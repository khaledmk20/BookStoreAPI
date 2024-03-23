using System.Data;
using System.Security.Claims;
using AutoMapper;
using BookStoreAPI.Data;
using BookStoreAPI.Dtos;
using BookStoreAPI.Helpers;
using BookStoreAPI.Models;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookStoreAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]

    public class BooksController : ControllerBase
    {
        private readonly DataContextDapper _dapper;
        private readonly IMapper _mapper;
        private readonly AuthHelper _auth;

        public BooksController(IConfiguration config)
        {
            _auth = new AuthHelper(config);
            _dapper = new DataContextDapper(config);
            _mapper = new Mapper(new MapperConfiguration(cfg =>
          {
              cfg.CreateMap<BookToAddDto, Book>();
          }));

        }

        [HttpGet()]
        [AllowAnonymous]
        public IEnumerable<Book> GetBooks(string? categoryName,
        string? searchValue = "",
        int? pageNumber = 1,
        int? pageSize = 10,
        string? sortOrder = "",
        int? bookId = 0)
        {

            string sql = @"EXECUTE BookSchema.spBooks_Get";

            string stringParameter = "";
            DynamicParameters sqlParams = new DynamicParameters();

            if (sortOrder is not null)
            {

                sqlParams.Add("@SortOrderParam", sortOrder, DbType.String);
                stringParameter += ", @SortOrder = @SortOrderParam";

            }
            if (pageNumber != 0)
            {
                sqlParams.Add("@pageNumberParam", pageNumber, DbType.Int32);
                stringParameter += ", @pageNumber = @pageNumberParam";

            }
            if (pageSize != 0)
            {
                sqlParams.Add("@PageSizeParam", pageSize, DbType.Int32);
                stringParameter += ", @PageSize = @PageSizeParam";

            }
            if (searchValue is not null)
            {
                sqlParams.Add("@SearchValueParam", searchValue, DbType.String);
                stringParameter += ", @SearchValue = @SearchValueParam";
            }

            if (categoryName is not null)
            {
                sqlParams.Add("@CategoryNameParam", categoryName, DbType.String);
                stringParameter += ", @CategoryName = @CategoryNameParam";

            }
            if (bookId != 0)
            {
                sqlParams.Add("@BookIdParam", bookId, DbType.Int32);
                stringParameter += ", @BookId = @BookIdParam";
            }
            if (stringParameter.Length > 0)
            {
                sql += stringParameter.Substring(1);
            }


            return _dapper.LoadDataWithParameters<Book>(sql, sqlParams);
        }

        [HttpPost()]
        public IActionResult AddBook(BookToAddDto bookToAdd)
        {
            if (!_auth.IsAdmin(User))
                return Unauthorized("Only admins can access this route");
            Book book = _mapper.Map<Book>(bookToAdd);
            book.Id = null;
            return HandleBookAddOrEdit(book);
        }


        [HttpPut()]
        public IActionResult EditBook(Book bookToEdit)
        {
            if (!_auth.IsAdmin(User))
                return Unauthorized("Only admins can access this route");

            if (bookToEdit.Id == 0)
                return BadRequest("Please specify a valid book Id");

            return HandleBookAddOrEdit(bookToEdit);
        }


        private IActionResult HandleBookAddOrEdit(Book book)
        {
            string sql = @"EXECUTE BookSchema.sp_Book_Upsert 
                    @BookId = @BookIdParam,
                    @BookTitle = @BookTitleParam,
                    @AuthorName = @AuthorNameParam,
                    @CategoryName = @CategoryNameParam,
                    @BookPrice = @BookPriceParam ";

            string sqlParameterString = "";
            DynamicParameters sqlParameter = new DynamicParameters();
            sqlParameter.Add("@BookIdParam", book.Id, DbType.Int32);
            sqlParameter.Add("@BookTitleParam", book.BookTitle, DbType.String);
            sqlParameter.Add("@AuthorNameParam", book.AuthorName, DbType.String);
            sqlParameter.Add("@CategoryNameParam", book.CategoryName, DbType.String);
            sqlParameter.Add("@BookPriceParam", book.BookPrice, DbType.Int32);

            if (book.BookDescription is not null)
            {
                sqlParameter.Add("@BookDescriptionParam", book.BookDescription, DbType.String);
                sqlParameterString += ", @BookDescription = @BookDescriptionParam ";

            }
            if (book.QuantityInStock is not null)
            {
                sqlParameter.Add("@QuantityInStockParam", book.QuantityInStock, DbType.Int32);
                sqlParameterString += ", @QuantityInStock = @QuantityInStockParam";
            }
            if (book.PublicationYear is not null)
            {
                sqlParameter.Add("@PublicationYearParam", book.PublicationYear, DbType.Int32);
                sqlParameterString += ", @PublicationYear = @PublicationYearParam";
            }
            if (book.BookImage is not null)
            {
                sqlParameter.Add("@BookImageParam", book.BookImage, DbType.String);
                sqlParameterString += ", @BookImage = @BookImageParam";
            }


            sql += sqlParameterString;


            if (_dapper.ExecuteSqlWithParameters(sql, sqlParameter))
            {
                return Ok();
            }

            if (book.Id == 0 || book.Id is null)
                throw new Exception("Failed to add book.");

            else
                throw new Exception("Failed to edit book.");
        }

        [HttpDelete("{Id}")]
        public IActionResult DeleteBook(int bookId)
        {
            if (!_auth.IsAdmin(User))
                return Unauthorized("Only admins can access this route");
            string sql = "UserSchema.spBook_delete  @BookId = @BookIdParam";

            DynamicParameters sqlParams = new DynamicParameters();
            sqlParams.Add("BookIdParam", bookId, DbType.Int32);

            if (_dapper.ExecuteSqlWithParameters(sql, sqlParams))
                return Ok();
            throw new Exception("Failed to delete book.");

        }


    }
    // TODO: Add book comments endpoint 
}



